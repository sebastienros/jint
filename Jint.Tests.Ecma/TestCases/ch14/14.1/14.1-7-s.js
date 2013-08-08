/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-7-s.js
 * @description 'use strict' directive - not recognized if upper case
 * @noStrict
 */


function testcase() {

  function foo()
  {
    'Use Strict';
     return (this !== undefined);
  }

  return foo.call(undefined);
 }
runTestCase(testcase);
