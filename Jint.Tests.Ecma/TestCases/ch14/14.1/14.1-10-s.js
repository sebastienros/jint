/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-10-s.js
 * @description other directives - may follow 'use strict' directive
 * @noStrict
 */


function testcase() {

  function foo()
  {
     "use strict";
     "bogus directive";
     return (this === undefined);
  }

  return foo.call(undefined);
 }
runTestCase(testcase);
