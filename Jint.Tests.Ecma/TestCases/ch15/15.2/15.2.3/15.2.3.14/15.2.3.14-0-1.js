/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-0-1.js
 * @description Object.keys must exist as a function
 */


function testcase() {
  var f = Object.keys;
  if (typeof(f) === "function") {
    return true;
  }
 }
runTestCase(testcase);
