/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-13.js
 * @description String.prototype.trim handles whitepace and lineterminators (abc\u000C)
 */


function testcase() {
  if ("abc\u000C".trim() === "abc") {
    return true;
  }
 }
runTestCase(testcase);
