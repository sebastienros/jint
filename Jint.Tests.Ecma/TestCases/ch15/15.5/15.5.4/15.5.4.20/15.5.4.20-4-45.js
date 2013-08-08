/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-45.js
 * @description String.prototype.trim handles whitepace and lineterminators (\u2028abc)
 */


function testcase() {
  if ("\u2028abc".trim() === "abc") {
    return true;
  }
 }
runTestCase(testcase);
