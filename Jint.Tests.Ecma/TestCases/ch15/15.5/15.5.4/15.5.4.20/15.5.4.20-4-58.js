/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-58.js
 * @description String.prototype.trim handles whitepace and lineterminators (\u2029\u2029)
 */


function testcase() {
  if ("\u2029\u2029".trim() === "") {
    return true;
  }
 }
runTestCase(testcase);
