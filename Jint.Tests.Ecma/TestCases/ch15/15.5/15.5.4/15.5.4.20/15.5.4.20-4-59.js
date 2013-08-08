/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-59.js
 * @description String.prototype.trim handles whitepace and lineterminators (\u2029abc as a multiline string)
 */


function testcase() {
  var s = "\u2029\
           abc";
  if (s.trim() === "abc") {
    return true;
  }
 }
runTestCase(testcase);
