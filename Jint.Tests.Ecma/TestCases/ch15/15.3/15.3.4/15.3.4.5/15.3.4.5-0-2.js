/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-0-2.js
 * @description Function.prototype.bind must exist as a function taking 1 parameter
 */


function testcase() {
  if (Function.prototype.bind.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
