/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-27.js
 * @description String.prototype.trim handles whitepace and lineterminators (\u0009\u0009)
 */


function testcase() {
  if ("\u0009\u0009".trim() === "") {
    return true;
  }
 }
runTestCase(testcase);
