/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-38.js
 * @description String.prototype.trim handles whitepace and lineterminators (ab\u0020c)
 */


function testcase() {
  if ("ab\u0020c".trim() === "ab\u0020c") {
    return true;
  }
 }
runTestCase(testcase);
