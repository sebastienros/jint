/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-0-1.js
 * @description Object.getOwnPropertyNames must exist as a function
 */


function testcase() {
  if (typeof(Object.getOwnPropertyNames) === "function") {
    return true;
  }
 }
runTestCase(testcase);
