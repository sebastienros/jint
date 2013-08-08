/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-14-s.js
 * @description semicolon insertion may come before 'use strict' directive
 * @noStrict
 */


function testcase() {

  function foo()
  {
    "another directive"
    "use strict" ;
    return (this === undefined);
  }

  return foo.call(undefined);
 }
runTestCase(testcase);
