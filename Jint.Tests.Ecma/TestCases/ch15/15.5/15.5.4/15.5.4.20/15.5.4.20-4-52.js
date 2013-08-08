/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-52.js
 * @description String.prototype.trim handles whitepace and lineterminators (\u000Dabc\u000D)
 */


function testcase() {
  if ("\u000Dabc\u000D".trim() === "abc") {
    return true;
  }
 }
runTestCase(testcase);
