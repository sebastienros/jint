/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-19.js
 * @description String.prototype.trim handles whitepace and lineterminators (\u0009abc\u0009)
 */


function testcase() {
  if ("\u0009abc\u0009".trim() === "abc") {
    return true;
  }
 }
runTestCase(testcase);
