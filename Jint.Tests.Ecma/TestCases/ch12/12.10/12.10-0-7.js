/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10-0-7.js
 * @description with introduces scope - scope removed when exiting with statement
 */


function testcase() {
  var o = {foo: 1};

  with (o) {
    foo = 42;
  }

  try {
    foo;
  }
  catch (e) {
     return true;
  }
 }
runTestCase(testcase);
