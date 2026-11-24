// One small helper for all calls to the API.
// It adds the login token, turns error answers into ApiError, and renews an expired token once.

// An error answer from the API.
// "code" is the stable error name (for example "Auth.InvalidCredentials"); we translate it for the user.
export class ApiError extends Error {
  status: number;
  code: string;
  fieldErrors: Record<string, string[]>;

  constructor(status: number, code: string, fieldErrors: Record<string, string[]> = {}) {
    super(code);
    this.status = status;
    this.code = code;
    this.fieldErrors = fieldErrors;
  }
}

// The auth code registers these two functions. The client only knows "how to get a token"
// and "how to renew it", not where tokens are kept.
let getAccessToken: () => string | null = () => null;
let renewAccessToken: () => Promise<boolean> = async () => false;

export function connectAuth(getToken: () => string | null, renew: () => Promise<boolean>) {
  getAccessToken = getToken;
  renewAccessToken = renew;
}

interface RequestOptions {
  method?: "GET" | "POST" | "PUT" | "DELETE";
  body?: unknown;
  // Login endpoints are called without a token.
  anonymous?: boolean;
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  let response = await send(path, options);

  // 401 with a token usually means the access token expired. Renew it once and try again.
  if (response.status === 401 && !options.anonymous && getAccessToken()) {
    const renewed = await renewAccessToken();
    if (renewed) {
      response = await send(path, options);
    }
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  // 204 means "done, nothing to send back".
  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

async function send(path: string, options: RequestOptions): Promise<Response> {
  const headers: Record<string, string> = {};

  if (options.body !== undefined) {
    headers["Content-Type"] = "application/json";
  }

  const token = options.anonymous ? null : getAccessToken();
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  try {
    return await fetch(path, {
      method: options.method ?? "GET",
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    });
  } catch {
    // fetch throws when the server cannot be reached at all.
    throw new ApiError(0, "Network.Unreachable");
  }
}

// The API sends errors in the standard "problem details" shape:
//   { "title": "Auth.InvalidCredentials", "status": 401, "detail": "..." }
// Validation errors also carry: { "errors": { "FieldName": ["message"] } }
export async function toApiError(response: Response): Promise<ApiError> {
  let body: any = null;
  try {
    body = await response.json();
  } catch {
    // The body was empty or not JSON. We still report the status below.
  }

  if (response.status === 429) {
    return new ApiError(429, "RateLimit.TooManyRequests");
  }

  const fieldErrors: Record<string, string[]> = body?.errors ?? {};

  // Service validation errors use the error code as the key inside "errors".
  const codeFromErrors = Object.keys(fieldErrors).find((key) => key.includes("."));
  const hasFieldErrors = Object.keys(fieldErrors).length > 0;

  let code = "General.Unexpected";
  if (codeFromErrors) {
    code = codeFromErrors;
  } else if (hasFieldErrors) {
    code = "Validation.Invalid";
  } else if (typeof body?.title === "string" && body.title.includes(".")) {
    code = body.title;
  } else if (response.status === 401) {
    code = "Auth.Unauthorized";
  } else if (response.status === 403) {
    code = "Auth.Forbidden";
  }

  return new ApiError(response.status, code, fieldErrors);
}
