/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-2-s.js
 * @description this is not coerced to an object in strict mode (string)
 * @noStrict
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


  return foo.call('1') === 'string' && bar.call('1') === 'object';
 }
runTestCase(testcase);
