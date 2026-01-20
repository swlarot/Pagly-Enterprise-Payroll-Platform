export interface JwtPayload {
  sub: string;
  email: string;
  tenant_id: string;
  tenant_role: string;
  plan: string;
  exp: number;
  iat: number;
}

export function parseJwt(token: string): JwtPayload | null {
  try {
    const base64Url = token.split('.')[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    return JSON.parse(jsonPayload);
  } catch (error) {
    console.error('Error parsing JWT:', error);
    return null;
  }
}

export function isTokenExpired(token: string): boolean {
  const payload = parseJwt(token);
  if (!payload) return true;

  const now = Date.now() / 1000;
  return payload.exp < now;
}
