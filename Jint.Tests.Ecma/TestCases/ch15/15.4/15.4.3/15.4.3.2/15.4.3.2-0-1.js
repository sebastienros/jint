/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-0-1.js
 * @description Array.isArray must exist as a function
 */


function testcase() {
  var f = Array.isArray;
  if (typeof(f) === "function") {
    return true;
  }
 }
runTestCase(testcase);
