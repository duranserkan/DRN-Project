import http from 'k6/http';
import { check, sleep } from 'k6';
import { generateRandomPassword, generateUsernameWithPrefix } from './Utils/auth.js'; // Import the utility function

//k6 run --out cloud private-scope-context.js --http-debug
//https://k6.io/docs/using-k6/test-lifecycle/

export const options = {
    // A number specifying the number of VUs to run concurrently.
    vus: 1,
    // A string specifying the total duration of the test run.
    duration: '5s',

    // The following section contains configuration options for execution of this
    // test script in Grafana Cloud.
    //
    // See https://grafana.com/docs/grafana-cloud/k6/get-started/run-cloud-tests-from-the-cli/
    // to learn about authoring and running k6 test scripts in Grafana k6 Cloud.

    cloud: {
        projectID: 3705162,
        // Test runs with the same name groups test runs together
        name: 'Private ScopeContext'
    }
};

// Define URLs
const registerUrl = 'http://localhost:5998/identity/register';
const loginUrl = 'http://localhost:5998/identity/login';
const protectedUrl = 'http://localhost:5998/private/scope-context';

// Generate random username and password
const randomUsername = generateUsernameWithPrefix('sample') + '@sample.com'; // Random username with email format
const randomPassword = generateRandomPassword(16); // Random password with 16 characters

// Define payloads for registration and login
const registrationPayload = JSON.stringify({
    email: randomUsername,
    password: randomPassword
});

// Function to register user and login to get token
export function setup() {
    // Step 1: Register the user
    let response = http.post(registerUrl, registrationPayload, {
        headers: { 'Content-Type': 'application/json' },
    });

    // Check if registration was successful
    check(response, {
        'registration status is 200': (r) => r.status === 200,
    });

    console.log(`Registration Response Status: ${response.status}`);
    console.log(`Registration Response Body: ${response.body}`);

    // Step 2: Log in to get the token
    response = http.post(loginUrl, registrationPayload, {
        headers: { 'Content-Type': 'application/json' },
    });

    console.log(`Login Response Status: ${response.status}`);
    console.log(`Login Response Body: ${response.body}`);

    // Check if login was successful and token is obtained
    check(response, {
        'login status is 200': (r) => r.status === 200,
    });

    const responseBody = response.json();
    const accessToken = responseBody.accessToken;

    // Verify that accessToken is present
    check(accessToken, {
        'accessToken is present': (token) => token !== undefined && token !== '',
    });

    // Return the accessToken to be used in the main function
    return { accessToken };
}

// Main test function
// The function that defines VU logic.
//
// See https://grafana.com/docs/k6/latest/examples/get-started-with-k6/ to learn more
// about authoring k6 scripts.
export default function (data) {
    // Extract the token from the setup function's return value
    const { accessToken } = data;

    // Step 3: Access the protected endpoint with the Bearer token
    const authHeaders = {
        headers: {
            'Authorization': `Bearer ${accessToken}`,
        },
    };

    let response = http.get(protectedUrl, authHeaders);

    // Check if the protected endpoint request was successful
    check(response, {
        'protected endpoint status is 200': (r) => r.status === 200,
    });

    //sleep(1); // Optional: Sleep to simulate user wait time
}