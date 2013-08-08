/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-41.js
 * @description String.prototype.trim handles whitepace and lineterminators (ab\u200Bc)
 */


function testcase() {
  if ("ab\u200Bc".trim() === "ab\u200Bc") {
    return true;
  }
 }
runTestCase(testcase);
