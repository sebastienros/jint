/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-2-5.js
 * @description Object.keys returns the standard built-in Array that is not sealed
 */


function testcase() {
  var o = { x: 1, y: 2};

  var a = Object.keys(o);
  if (Object.isSealed(a) === false) {
    return true;
  }
 }
runTestCase(testcase);
