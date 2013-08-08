/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-35.js
 * @description String.prototype.trim handles whitepace and lineterminators (ab\u0009c)
 */


function testcase() {
  if ("ab\u0009c".trim() === "ab\u0009c") {
    return true;
  }
 }
runTestCase(testcase);
