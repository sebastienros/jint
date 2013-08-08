/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-8-s.js
 * @description 'use strict' directive - may follow other directives
 * @noStrict
 */


function testcase() {

  function foo()
  {
     "bogus directive";
     "use strict";
     return (this === undefined);
  }

  return foo.call(undefined);
 }
runTestCase(testcase);
