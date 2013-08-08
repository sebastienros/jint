/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-5-s.js
 * @description 'use strict' directive - not recognized if contains a EscapeSequence
 * @noStrict
 */


function testcase() {

  function foo()
  {
    'use\u0020strict';
     return(this !== undefined);
  }

  return foo.call(undefined);
 }
runTestCase(testcase);
