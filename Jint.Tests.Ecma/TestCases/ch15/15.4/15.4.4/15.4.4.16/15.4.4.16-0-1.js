/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-0-1.js
 * @description Array.prototype.every must exist as a function
 */


function testcase() {
  var f = Array.prototype.every;
  if (typeof(f) === "function") {
    return true;
  }
 }
runTestCase(testcase);
