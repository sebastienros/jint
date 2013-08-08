/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * local vars must not be visible outside with block
 * local functions must not be visible outside with block
 * local function expresssions should not be visible outside with block
 * local vars must shadow outer vars
 * local functions must shadow outer functions
 * local function expresssions must shadow outer function expressions
 * eval should use the appended object to the scope chain
 *
 * @path ch12/12.14/12.14-8.js
 * @description catch introduces scope - scope removed when exiting catch block (properties)
 */


function testcase() {
  var o = {foo: 42};

  try {
    throw o;
  }
  catch (e) {
    var foo = 1;
  }

  if (o.foo === 42) {
    return true;
  }
 }
runTestCase(testcase);
