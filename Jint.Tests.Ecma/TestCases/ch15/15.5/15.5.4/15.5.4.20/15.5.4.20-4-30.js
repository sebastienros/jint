/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-30.js
 * @description String.prototype.trim handles whitepace and lineterminators (\u0020\u0020)
 */


function testcase() {
  if ("\u0020\u0020".trim() === "") {
    return true;
  }
 }
runTestCase(testcase);
