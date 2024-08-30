import http from 'k6/http';
import { check, sleep } from 'k6';

//k6 run --out cloud private-validate-scope.js
export const options = {
  // A number specifying the number of VUs to run concurrently.
  vus: 100,
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
    name: 'Private ValidateScope'
  }
};

// The function that defines VU logic.
//
// See https://grafana.com/docs/k6/latest/examples/get-started-with-k6/ to learn more
// about authoring k6 scripts.
//
export default function() {
  let response = http.get('http://localhost:5998/private/validate-scope');

  check(response, {
    'is status 200': r => r.status === 200,
  })

  //sleep(1); // Optional: Sleep to simulate user wait time
}