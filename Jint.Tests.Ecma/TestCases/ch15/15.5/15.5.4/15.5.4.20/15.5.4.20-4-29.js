/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-29.js
 * @description String.prototype.trim handles whitepace and lineterminators (\u000C\u000C)
 */


function testcase() {
  if ("\u000C\u000C".trim() === "") {
    return true;
  }
 }
runTestCase(testcase);
