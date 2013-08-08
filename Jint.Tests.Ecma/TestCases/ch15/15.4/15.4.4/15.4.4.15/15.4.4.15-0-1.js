/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-0-1.js
 * @description Array.prototype.lastIndexOf must exist as a function
 */


function testcase() {
  var f = Array.prototype.lastIndexOf;
  if (typeof(f) === "function") {
    return true;
  }
 }
runTestCase(testcase);
