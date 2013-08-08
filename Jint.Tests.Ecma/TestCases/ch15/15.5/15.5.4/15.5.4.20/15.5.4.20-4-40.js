/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-40.js
 * @description String.prototype.trim handles whitepace and lineterminators (ab\u00A0c)
 */


function testcase() {
  if ("ab\u00A0c".trim() === "ab\u00A0c") {
    return true;
  }
 }
runTestCase(testcase);
