/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-17-s.js
 * @description 'use strict' directive - not recognized if it follow some other statment empty statement
 * @noStrict
 */

function testcase() {

  function foo()
  {
    var x;
    'use strict';
    return (this !== undefined);
  }

  return foo.call(undefined);
 }
runTestCase(testcase);
