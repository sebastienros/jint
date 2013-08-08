/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-27.js
 * @description Object.isFrozen returns false for all built-in objects (JSON)
 */


function testcase() {
  var b = Object.isFrozen(JSON);
  if (b === false) {
    return true;
  }
 }
runTestCase(testcase);
