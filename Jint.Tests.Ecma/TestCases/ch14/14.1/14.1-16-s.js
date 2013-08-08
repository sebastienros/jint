/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-16-s.js
 * @description 'use strict' directive - not recognized if it follow an empty statement
 * @noStrict
 */


function testcase() {

  function foo()
  {
    ; 'use strict';
     return (this !== undefined);
  }

  return foo.call(undefined);
 }
runTestCase(testcase);
