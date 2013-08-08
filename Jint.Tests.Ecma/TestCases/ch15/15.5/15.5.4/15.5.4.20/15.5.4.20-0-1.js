/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-0-1.js
 * @description String.prototype.trim must exist as a function
 */


function testcase() {
  var f = String.prototype.trim;
  if (typeof(f) === "function") {
    return true;
  }
 }
runTestCase(testcase);
