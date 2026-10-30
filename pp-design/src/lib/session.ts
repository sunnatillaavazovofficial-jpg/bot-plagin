import { SignJWT, jwtVerify } from "jose";
import { cookies } from "next/headers";
import { isHttps, sessionSecret } from "./env";

export const SESSION_COOKIE = "pp_session";
const MAX_AGE = 60 * 60 * 24 * 30; // 30 kun

/** Parol bilan kirgan admin uchun bazada yozuv yo'q — sessiyada shu maxsus ID turadi. */
export const PASSWORD_ADMIN_ID = "password-admin";

export type SessionPayload = { uid: string };

export async function createSession(uid: string): Promise<void> {
  const token = await new SignJWT({ uid })
    .setProtectedHeader({ alg: "HS256" })
    .setIssuedAt()
    .setExpirationTime(`${MAX_AGE}s`)
    .sign(sessionSecret());

  const store = await cookies();
  store.set(SESSION_COOKIE, token, {
    httpOnly: true,
    secure: isHttps(),
    sameSite: "lax",
    path: "/",
    maxAge: MAX_AGE,
  });
}

export async function readSession(): Promise<SessionPayload | null> {
  const store = await cookies();
  const token = store.get(SESSION_COOKIE)?.value;
  if (!token) return null;
  try {
    const { payload } = await jwtVerify(token, sessionSecret());
    return typeof payload.uid === "string" ? { uid: payload.uid } : null;
  } catch {
    return null;
  }
}

export async function destroySession(): Promise<void> {
  const store = await cookies();
  store.delete(SESSION_COOKIE);
}
