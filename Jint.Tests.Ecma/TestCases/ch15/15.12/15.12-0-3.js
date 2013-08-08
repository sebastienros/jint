/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * This test should be run without any built-ins being added/augmented.
 * The name JSON must be bound to an object, and must not support [[Call]].
 * step 5 in 11.2.3 should throw a TypeError exception.
 *
 * @path ch15/15.12/15.12-0-3.js
 * @description JSON must not support the [[Call]] method
 */


function testcase() {
  var o = JSON;

  try {
    var j = JSON();
  }
  catch (e) {
    if (e instanceof TypeError) {
      return true;
    }
  }
 }
runTestCase(testcase);
