// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Appearing of continue without an IterationStatement leads to syntax error
 *
 * @path ch12/12.7/S12.7_A1_T3.js
 * @description Checking if laballed "continue" with no IterationStatement, placed into a block, fails
 * @negative
 */

LABEL : x=3.14;

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
{
    var x=1;
    continue LABEL;
    var y=2;
}
//
//////////////////////////////////////////////////////////////////////////////

