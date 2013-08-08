/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-0-1.js
 * @description Object.defineProperty must exist as a function
 */


function testcase() {
  var f = Object.defineProperty;
  if (typeof(f) === "function") {
    return true;
  }
 }
runTestCase(testcase);
