/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-15-s.js
 * @description blank lines may come before 'use strict' directive
 * @noStrict
 */


function testcase() {

  function foo()
  {






    "use strict" ;
    return (this === undefined);
  }

  return foo.call(undefined);
 }
runTestCase(testcase);
