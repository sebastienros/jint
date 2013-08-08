/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * 15.3.4.5 step 2 specifies that a TypeError must be thrown if the Target is not callable.
 *
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-2.js
 * @description Function.prototype.bind throws TypeError if the Target is not callable (bind attached to object)
 */


function testcase() {
  // dummy function 
  function foo() {}
  var f = new foo();
  f.bind = Function.prototype.bind;

  try {
    f.bind();
  }
  catch (e) {
    if (e instanceof TypeError) {
      return true;
    }
  }
 }
runTestCase(testcase);
