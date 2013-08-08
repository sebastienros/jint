/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-49.js
 * @description String.prototype.trim handles whitepace and lineterminators (abc\u2028)
 */


function testcase() {
  if ("abc\u2028".trim() === "abc") {
    return true;
  }
 }
runTestCase(testcase);
