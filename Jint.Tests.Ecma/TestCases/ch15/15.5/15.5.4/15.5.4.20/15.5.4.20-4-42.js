/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-42.js
 * @description String.prototype.trim handles whitepace and lineterminators (ab\uFEFFc)
 */


function testcase() {
  if ("ab\uFEFFc".trim() === "ab\uFEFFc") {
    return true;
  }
 }
runTestCase(testcase);
