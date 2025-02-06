function urlSafeBase64Encode(str) {
  // Base64 encode the string
  const base64 = btoa(str);

  // Convert to URL-safe Base64
  // Remove padding '=' characters
  return base64
      .replace(/\+/g, '-')  // Replace '+' with '-'
      .replace(/\//g, '_')  // Replace '/' with '_'
      .replace(/=+$/, '');
}

function urlSafeBase64Decode(str) {
  // Replace URL-safe characters back to standard Base64 characters
  const base64 = str
      .replace(/-/g, '+')  // Replace '-' with '+'
      .replace(/_/g, '/')  // Replace '_' with '/'
      .padEnd(str.length + (4 - str.length % 4) % 4, '=');  // Add padding if necessary

  // Decode Base64 string
  return atob(base64);
}

function checkCookieExists (cookieName) {
  document.cookie.split('; ').some(cookie => cookie.startsWith(`${cookieName}=`));
} 