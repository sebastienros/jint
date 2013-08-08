/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-55.js
 * @description String.prototype.trim handles whitepace and lineterminators (\u000A\u000A)
 */


function testcase() {
  if ("\u000A\u000A".trim() === "") {
    return true;
  }
 }
runTestCase(testcase);
