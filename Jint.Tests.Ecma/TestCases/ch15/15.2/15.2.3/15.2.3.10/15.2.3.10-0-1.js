/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-0-1.js
 * @description Object.preventExtensions must exist as a function
 */


function testcase() {
  var f = Object.preventExtensions;
  if (typeof(f) === "function") {
    return true;
  }
 }
runTestCase(testcase);
