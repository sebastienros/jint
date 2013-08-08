/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-3-18.js
 * @description Object.isFrozen returns false for all built-in objects (RegExp.prototype)
 */


function testcase() {
  var b = Object.isFrozen(RegExp.prototype);
  if (b === false) {
    return true;
  }
 }
runTestCase(testcase);
