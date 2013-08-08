/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-0-1.js
 * @description Object.getOwnPropertyDescriptor must exist as a function
 */


function testcase() {
  if (typeof(Object.getOwnPropertyDescriptor) === "function") {
    return true;
  }
 }
runTestCase(testcase);
