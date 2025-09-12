export interface GeneratePasswordResetLinkRequestDto {
  username: string;
}

export interface GeneratePasswordResetLinkResponseDto {
  resetLink: string;
  expiresAt: string;
}

export interface UsePasswordResetLinkRequestDto {
  token: string;
  newPassword: string;
}