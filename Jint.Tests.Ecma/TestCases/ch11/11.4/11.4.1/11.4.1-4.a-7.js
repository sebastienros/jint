/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * This test is actually testing the [[Delete]] internal method (8.12.8). Since the
 * language provides no way to directly exercise [[Delete]], the tests are placed here.
 *
 * @path ch11/11.4/11.4.1/11.4.1-4.a-7.js
 * @description delete operator inside 'eval'
 */


function testcase() {
  var x = 1;
  var d = eval("delete x");
  if (d === false && x === 1) {
    return true;
  }
  return false;
 }
runTestCase(testcase);
