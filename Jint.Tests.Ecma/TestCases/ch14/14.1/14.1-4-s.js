/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-4-s.js
 * @description 'use strict' directive - not recognized if contains Line Continuation
 * @noStrict
 */


function testcase() {

  function foo()
  {
    'use str\
ict';
     return (this !== undefined);
  }

  return foo.call(undefined);
 }
runTestCase(testcase);
