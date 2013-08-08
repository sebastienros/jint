/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-5-s.js
 * @description this is not coerced to an object in strict mode (function)
 * @onlyStrict
 */


function testcase() {

  function foo()
  {
    'use strict';
    return typeof(this);
  } 

  function bar()
  {
    return typeof(this);
  }

  function foobar()
  {
  }

  return foo.call(foobar) === 'function' && bar.call(foobar) === 'function';
 }
runTestCase(testcase);
