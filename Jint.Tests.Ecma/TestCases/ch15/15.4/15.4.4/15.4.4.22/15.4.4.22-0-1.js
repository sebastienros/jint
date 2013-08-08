/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-0-1.js
 * @description Array.prototype.reduceRight must exist as a function
 */


function testcase() {
  var f = Array.prototype.reduceRight;
  if (typeof(f) === "function") {
    return true;
  }
 }
runTestCase(testcase);
