/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-1.js
 * @description String.prototype.trim handles multiline string with whitepace and lineterminators
 */


function testcase() {
var s = "\u0009a b\
c \u0009"

            
  if (s.trim() === "a bc") {
    return true;
  }
 }
runTestCase(testcase);
