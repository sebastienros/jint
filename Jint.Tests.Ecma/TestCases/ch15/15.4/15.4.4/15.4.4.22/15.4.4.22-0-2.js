/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-0-2.js
 * @description Array.prototype.reduceRight.length must be 1
 */


function testcase() {
  if (Array.prototype.reduceRight.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
