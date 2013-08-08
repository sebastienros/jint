/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-0-1.js
 * @description Function.prototype.bind must exist as a function
 */


function testcase() {
  var f = Function.prototype.bind;

  if (typeof(f) === "function") {
    return true;
  }
 }
runTestCase(testcase);
