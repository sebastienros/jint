/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-0-1.js
 * @description Array.prototype.filter must exist as a function
 */


function testcase() {
  var f = Array.prototype.filter;
  if (typeof(f) === "function") {
    return true;
  }
 }
runTestCase(testcase);
