// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Nested function are admitted
 *
 * @path ch13/13.2/S13.2_A2_T2.js
 * @description Nesting level is three
 */

var __ROBOT="C3PO";

function __FUNC(){
    function __GUNC(){
        return arguments[0];
    };
    function __HUNC(){
        return __GUNC;
    };
    return __HUNC;
};

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
if (__FUNC()()(__ROBOT) !== __ROBOT) {
	$ERROR('#1: __FUNC()()(__ROBOT) === __ROBOT. Actual: __FUNC()()(__ROBOT) ==='+__FUNC()()(__ROBOT));
}
//
//////////////////////////////////////////////////////////////////////////////

