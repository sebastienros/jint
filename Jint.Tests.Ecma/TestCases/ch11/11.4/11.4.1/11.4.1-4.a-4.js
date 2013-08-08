/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * This test is actually testing the [[Delete]] internal method (8.12.8). Since the
 * language provides no way to directly exercise [[Delete]], the tests are placed here.
 *
 * @path ch11/11.4/11.4.1/11.4.1-4.a-4.js
 * @description delete operator returns false when deleting a non-configurable data property (NaN)
 */


function testcase() {
  // NaN (15.1.1.1) has [[Configurable]] set to false.
  var d = delete NaN;
  if (d === false) {
    return true;
  }
 }
runTestCase(testcase);
