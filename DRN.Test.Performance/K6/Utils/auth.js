import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

export function generateRandomPassword(length) {
    if (length < 4) {
        throw new Error('Length should be at least 4 to include all required character types.');
    }

    // Define character sets
    const lowercase = 'abcdefghijklmnopqrstuvwxyz';
    const uppercase = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
    const digits = '0123456789';
    const specialChars = '!@#$%^&*()_+-=[]{}|;:,.<>?';

    // Ensure we have at least one of each type
    const guaranteedChars = [
        lowercase.charAt(Math.floor(Math.random() * lowercase.length)),
        uppercase.charAt(Math.floor(Math.random() * uppercase.length)),
        digits.charAt(Math.floor(Math.random() * digits.length)),
        specialChars.charAt(Math.floor(Math.random() * specialChars.length))
    ];

    // Fill the rest of the string with random characters from the full set
    const allChars = lowercase + uppercase + digits + specialChars;
    let result = '';
    for (let i = 0; i < length - 4; i++) {
        result += allChars.charAt(Math.floor(Math.random() * allChars.length));
    }

    // Combine guaranteed characters and shuffle the result
    result = result.split('').concat(guaranteedChars).sort(() => Math.random() - 0.5).join('');

    return result;
}

// Function to generate a random username with a prefix and GUID
export function generateUsernameWithPrefix(prefix) {
    if (!prefix || typeof prefix !== 'string') {
        throw new Error('Prefix must be a non-empty string.');
    }
    // Generate a UUID and use it as part of the username
    const uuid = generateUUID();
    // Construct the username with prefix and UUID
    return `${prefix}-${uuid}`;
}

// Function to generate a UUID (GUID)
function generateUUID() {
    // Create a UUID (v4) using crypto API
    const randomUUID = uuidv4();
    return randomUUID;
}