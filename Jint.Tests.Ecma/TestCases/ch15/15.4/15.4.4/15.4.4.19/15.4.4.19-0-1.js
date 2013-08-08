/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-0-1.js
 * @description Array.prototype.map must exist as a function
 */


function testcase() {
  var f = Array.prototype.map;
  if (typeof(f) === "function") {
    return true;
  }
 }
runTestCase(testcase);
