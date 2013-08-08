/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-34.js
 * @description String.prototype.trim handles whitepace and lineterminators (\uFEFF\uFEFF)
 */


function testcase() {
  return "\uFEFF\uFEFF".trim() === "";
 }
runTestCase(testcase);
