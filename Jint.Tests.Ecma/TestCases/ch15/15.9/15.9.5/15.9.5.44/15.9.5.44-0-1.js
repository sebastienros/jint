/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.9/15.9.5/15.9.5.44/15.9.5.44-0-1.js
 * @description Date.prototype.toJSON must exist as a function
 */


function testcase() {
  var f = Date.prototype.toJSON;
  if (typeof(f) === "function") {
    return true;
  }
 }
runTestCase(testcase);
