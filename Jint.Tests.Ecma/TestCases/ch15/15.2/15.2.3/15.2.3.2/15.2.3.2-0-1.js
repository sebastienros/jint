/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-0-1.js
 * @description Object.getPrototypeOf must exist as a function
 */


function testcase() {
  if (typeof(Object.getPrototypeOf) === "function") {
    return true;
  }
 }
runTestCase(testcase);
